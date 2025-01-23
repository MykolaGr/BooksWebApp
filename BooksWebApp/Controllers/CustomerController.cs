using Microsoft.AspNetCore.Mvc;
using BooksWebApp.Models.Books;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using BooksWebApp.Models;
using BooksWebApp.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace BooksWebApp.Controllers
{
    public class CustomerController : Controller
    {
        private readonly BooksContext _context;

        public CustomerController(BooksContext context)
        {
            _context = context;
        }

        // GET: Customer
public async Task<IActionResult> Index(int page = 1, int size = 20)
{
    // Step 1: Fetch raw data from the database
    var rawData = await (from customer in _context.Customers
                         join customerAddress in _context.CustomerAddresses
                             on customer.CustomerId equals customerAddress.CustomerId into customerAddresses
                         from customerAddress in customerAddresses.DefaultIfEmpty() // Left join
                         join address in _context.Addresses
                             on customerAddress.AddressId equals address.AddressId into addresses
                         from address in addresses.DefaultIfEmpty() // Left join
                         join country in _context.Countries
                             on address.CountryId equals country.CountryId into countries
                         from country in countries.DefaultIfEmpty() // Left join
                         select new
                         {
                             CustomerId = customer.CustomerId,
                             FirstName = customer.FirstName,
                             LastName = customer.LastName,
                             Email = customer.Email,
                             CountryName = country != null ? country.CountryName : "Unknown",
                             OrderCount = _context.CustOrders.Count(o => o.CustomerId == customer.CustomerId)
                         }).ToListAsync();

    // Step 2: Group and deduplicate in memory
    var groupedData = rawData
        .GroupBy(c => c.CustomerId)
        .Select(g => new CustomerViewModel
        {
            CustomerId = g.Key,
            FirstName = g.First().FirstName,
            LastName = g.First().LastName,
            Email = g.First().Email,
            CountryName = g.Select(c => c.CountryName).FirstOrDefault(), // Choose one country
            OrderCount = g.First().OrderCount
        })
        .OrderBy(c => c.LastName) // Sorting in memory
        .Skip((page - 1) * size)
        .Take(size)
        .ToList();

    // Step 3: Return paginated results
    var totalCustomers = rawData.Select(c => c.CustomerId).Distinct().Count();
    var pagingList = new PagingListAsync<CustomerViewModel>(groupedData.AsQueryable(), totalCustomers, page, size);

    return View(pagingList);
}


        public async Task<IActionResult> Orders(int id)
        {
            var customerExists = await _context.Customers.AnyAsync(c => c.CustomerId == id);

            if (!customerExists)
            {
                return NotFound();
            }

            var orders = await _context.CustOrders
                .Where(o => o.CustomerId == id)
                .Select(o => new OrderViewModel
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate
                })
                .ToListAsync();

            ViewBag.CustomerId = id;

            return View(orders);
        }
        // Get Edit
        public async Task<IActionResult> Edit(int id)
        {
            // Find the customer by ID
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            // Fetch the customer's associated country
            var customerCountry = await _context.CustomerAddresses
                .Where(ca => ca.CustomerId == id)
                .Join(
                    _context.Addresses,
                    ca => ca.AddressId,
                    address => address.AddressId,
                    (ca, address) => address.CountryId
                )
                .Join(
                    _context.Countries,
                    countryId => countryId,
                    country => country.CountryId,
                    (countryId, country) => country
                )
                .FirstOrDefaultAsync();

            // Load the country dropdown
            ViewBag.Countries = new SelectList(_context.Countries, "CountryId", "CountryName", customerCountry?.CountryId);

            return View(customer);
        }

        // Post Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CustomerId,FirstName,LastName,Email")] Customer customer, int? countryId)
        {
            if (id != customer.CustomerId)
            {
                return NotFound();
            }

            if (!countryId.HasValue)
            {
                ModelState.AddModelError("countryId", "Please select a country.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update customer
                    _context.Update(customer);
                    await _context.SaveChangesAsync();

                    // Update country association
                    var existingAddress = await _context.CustomerAddresses.FirstOrDefaultAsync(ca => ca.CustomerId == id);

                    if (existingAddress != null)
                    {
                        // Remove the old address association
                        _context.CustomerAddresses.Remove(existingAddress);
                        await _context.SaveChangesAsync();
                    }

                    // Add the new address association
                    var newAddress = new CustomerAddress
                    {
                        CustomerId = customer.CustomerId,
                        AddressId = countryId.Value,
                        StatusId = 1 // Default status
                    };
                    _context.CustomerAddresses.Add(newAddress);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            // Reload country dropdown in case of error
            ViewBag.Countries = new SelectList(_context.Countries, "CountryId", "CountryName", countryId);
            return View(customer);
        }


        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }

        // Get Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerId == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // Post Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            try
            {
                // Step 1: Remove order histories
                var orders = _context.CustOrders.Where(o => o.CustomerId == id);
                var orderIds = orders.Select(o => o.OrderId).ToList();
                var orderHistories = _context.OrderHistories.Where(oh => orderIds.Contains((int)oh.OrderId!));
                _context.OrderHistories.RemoveRange(orderHistories);

                // Step 2: Remove order lines
                var orderLines = _context.OrderLines.Where(ol => orderIds.Contains((int)ol.OrderId!));
                _context.OrderLines.RemoveRange(orderLines);

                // Step 3: Remove orders
                _context.CustOrders.RemoveRange(orders);

                // Step 4: Remove customer addresses
                var addresses = _context.CustomerAddresses.Where(ca => ca.CustomerId == id);
                _context.CustomerAddresses.RemoveRange(addresses);

                // Step 5: Remove the customer
                _context.Customers.Remove(customer);

                // Save changes
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error during deletion: {ex.Message}");
                return RedirectToAction(nameof(Index), new { error = "Unable to delete customer due to related records." });
            }

            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> EditStatus(int id)
        {
            var order = await _context.CustOrders
                .Where(o => o.OrderId == id)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            var statusList = await _context.OrderStatuses.ToListAsync();
            ViewBag.Statuses = statusList;

            var viewModel = new EditStatusViewModel
            {
                OrderId = order.OrderId,
                CurrentStatus = await _context.OrderStatuses
                    .Where(s => s.StatusId == order.ShippingMethodId)
                    .Select(s => s.StatusValue)
                    .FirstOrDefaultAsync(),
                CustomerId = order.CustomerId
            };

            return View(viewModel);
        }

        // POST: Customer/EditStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStatus(EditStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Repopulate dropdown list and current status
                ViewBag.Statuses = await _context.OrderStatuses.ToListAsync();
                model.CurrentStatus = await _context.OrderStatuses
                    .Where(s => s.StatusId == model.NewStatusId)
                    .Select(s => s.StatusValue)
                    .FirstOrDefaultAsync();
                return View(model);
            }

            var order = await _context.CustOrders
                .Where(o => o.OrderId == model.OrderId)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            if (model.NewStatusId < 4)
            {
                order.ShippingMethodId = model.NewStatusId;

                // Generate a unique HistoryId
                var uniqueHistoryId = Guid.NewGuid().GetHashCode(); // Use a GUID-based ID
                var history = new BooksWebApp.Models.Books.OrderHistory
                {
                    HistoryId = uniqueHistoryId, // Set the unique ID
                    OrderId = order.OrderId,
                    StatusId = model.NewStatusId,
                    StatusDate = DateTime.Now
                };

                _context.OrderHistories.Add(history);

                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Orders", new { id = order.CustomerId });
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", "An error occurred while saving the changes. Please try again.");
                }
            }
            else
            {
                ModelState.AddModelError("", "Cannot change status to a value of 4 or higher.");
            }

            // Repopulate dropdown list and update the current status for redisplay
            ViewBag.Statuses = await _context.OrderStatuses.ToListAsync();
            model.CurrentStatus = await _context.OrderStatuses
                .Where(s => s.StatusId == order.ShippingMethodId)
                .Select(s => s.StatusValue)
                .FirstOrDefaultAsync();

            return View(model);
        }
        // GET: Customer/Create
        public IActionResult Create()
        {
            //ViewBag.Countries = new SelectList(_context.Countries, "CountryId", "CountryName");
            return View();
        }


            
        // POST: Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,Email")] Customer customer, int? countryId)
        {
            if (!countryId.HasValue)
            {
                ModelState.AddModelError("countryId", "Please select a country.");
            }

            if (ModelState.IsValid)
            {
                // Generate a unique CustomerId
                var maxId = await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0;
                customer.CustomerId = maxId + 1;

                // Save the customer
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Example country association logic (if needed later)
                var address = new CustomerAddress
                {
                    CustomerId = customer.CustomerId,
                    AddressId = countryId.Value,
                    StatusId = 1 // Default status
                };
                _context.CustomerAddresses.Add(address);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // Reload dropdown list if validation fails
            ViewBag.Countries = new SelectList(_context.Countries, "CountryId", "CountryName");
            return View(customer);
        }



    }
    public class OrderViewModel
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
    }
    public class EditStatusViewModel
    {
        public int OrderId { get; set; }
        public int NewStatusId { get; set; }
        public string? CurrentStatus { get; set; }
        public int CustomerId { get; set; } 
    }
    
}