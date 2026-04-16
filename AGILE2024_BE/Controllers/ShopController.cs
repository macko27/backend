using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Identity;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models;
using AGILE2024_BE.Services;
using Microsoft.AspNetCore.SignalR;
using AGILE2024_BE.Models.Recognition;
using Azure.Storage.Blobs;
using AGILE2024_BE.Models.Shop;
using System.Net.Mail;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations.Schema;
using Azure.Core;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ShopController : Controller
    {

        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;
        private readonly IHubContext<NotificationHub> hubContext;


        public ShopController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db, IHubContext<NotificationHub> hubContext)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
            this.hubContext = hubContext;
        }



        //**********************************************************************************
        // Vytvorenie produktu
        //**********************************************************************************
        [HttpPost("Save")]
        [Authorize(Roles = RolesDef.ShopAdmin)]
        public async Task<IActionResult> SaveProduct([FromForm] ProductWithFileRequest data)
        {
            Product product;

            if (data.id.HasValue && data.id.Value != Guid.Empty) // ak ID existuje, nájdeme produkt
            {
                product = await dbContext.Products
                    .Include(p => p.ProductAttachment)
                    .FirstOrDefaultAsync(p => p.Id == data.id);
                if (product == null)
                    return BadRequest("Produkt neexistuje");
            }
            else // ak ID neexistuje, vytvoríme nový
            {
                product = new Product
                {
                    Id = Guid.NewGuid(),
                    ShopCategory = await dbContext.ShopCategories.FirstOrDefaultAsync(c => c.Id == data.shopCategoryId)
                };
                dbContext.Products.Add(product);
            }

            // aktualizujeme hodnoty
            product.Name = data.name;
            product.Info = data.info;
            product.Price = data.price;
            product.ShopCategory = await dbContext.ShopCategories.FirstOrDefaultAsync(c => c.Id == data.shopCategoryId);
            product.AnoPlatny = true;

            // súbor (ak je nový)
            if (data.file != null)
            {
                BlobServiceClient client = new(config.GetSection("Blob")["BlobConnect"]);
                var container = client.GetBlobContainerClient("product");

                var fileName = $"{Guid.NewGuid()}.{data.file.FileName.Split('.').Last()}";
                var blobClient = container.GetBlobClient(fileName);
                await blobClient.UploadAsync(data.file.OpenReadStream(), true);

                var attachment = new ProductAttachment
                {
                    FileName = data.file.FileName,
                    FileUrl = blobClient.Uri.ToString(),
                    ProductId = product.Id
                };

                var oldAttachment = await dbContext.ProductAttachments
                    .FirstOrDefaultAsync(a => a.ProductId == product.Id);

                if (oldAttachment != null)
                {
                    BlobClient oldBlob = container.GetBlobClient(new Uri(oldAttachment.FileUrl).Segments.Last());
                    await oldBlob.DeleteIfExistsAsync();

                    dbContext.ProductAttachments.Remove(oldAttachment);
                }

                dbContext.ProductAttachments.Add(attachment);
            }

            await dbContext.SaveChangesAsync();
            return Ok(new { id = product.Id });
        }



        //******************************
        // Získanie všetkých produktov
        //******************************
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllProducts(int page = 0, int size = 12)
        {
            var query = dbContext.Products
                .Include(p => p.ShopCategory)
                .Include(p => p.ProductAttachment)
                .Where(p => p.AnoPlatny == true)
                .AsQueryable();

            var products = await query
                .Skip(page * size)
                .Take(size)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    info = p.Info,
                    price = p.Price,
                    shopCategory = new
                    {
                        id = p.ShopCategory.Id,
                        name = p.ShopCategory.Name
                    },
                    productAttachment = p.ProductAttachment != null ? new
                    {
                        fileName = p.ProductAttachment.FileName,
                        fileUrl = p.ProductAttachment.FileUrl
                    } : null
                })
                .ToListAsync();

            return Ok(products);
        }




        //******************************
        // Získanie produktu
        //******************************
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            var product = await dbContext.Products
                .Include(p => p.ShopCategory)
                .Include(p => p.ProductAttachment)
                .Where(p => p.Id == id && p.AnoPlatny == true)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    info = p.Info,
                    price = p.Price,
                    shopCategory = new
                    {
                        id = p.ShopCategory.Id,
                        name = p.ShopCategory.Name
                    },
                    productAttachment = p.ProductAttachment != null ? new
                    {
                        fileName = p.ProductAttachment.FileName,
                        fileUrl = p.ProductAttachment.FileUrl
                    } : null
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }



        //******************************
        // Odstránenie produktu
        //******************************
        [HttpDelete("Delete/{id}")]
        [Authorize(Roles = RolesDef.ShopAdmin)]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var product = await dbContext.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound("Produkt nebol nájdený");

            product.AnoPlatny = false;

            await dbContext.SaveChangesAsync();

            return Ok("Produkt bol deaktivovaný");
        }


        //******************************
        // Získanie všetkých produktov
        //******************************
        [HttpGet("GetAllCategories")]
        public async Task<IActionResult> GetAllCategories()
        {
            var shopCategories = await dbContext.ShopCategories
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                })
                .ToListAsync();

            return Ok(shopCategories);
        }


        //*****************************************************
        // Vytvorenie novej kategórie
        //*****************************************************
        [HttpPost("CreateCategory")]
        [Authorize(Roles = RolesDef.ShopAdmin)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Názov kategórie nemôže byť prázdny");

            var category = new ShopCategory
            {
                Id = Guid.NewGuid(),
                Name = request.Name
            };

            dbContext.ShopCategories.Add(category);
            await dbContext.SaveChangesAsync();

            return Ok(new { id = category.Id, name = category.Name });
        }


        //*****************************************************
        // Odstránenie kategórie
        //*****************************************************
        [HttpDelete("DeleteCategory/{id}")]
        [Authorize(Roles = RolesDef.ShopAdmin)]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var category = await dbContext.ShopCategories.FindAsync(id);
            if (category == null)
                return NotFound("Kategória neexistuje");

            // Môžeš tu pridať kontrolu, či neexistujú produkty priradené ku kategórii
            var hasProducts = await dbContext.Products.AnyAsync(p => p.ShopCategory.Id == id);
            if (hasProducts)
                return BadRequest("Kategóriu nemožno odstrániť, pretože obsahuje produkty");

            dbContext.ShopCategories.Remove(category);
            await dbContext.SaveChangesAsync();

            return Ok("Kategória bola odstránená");
        }


        //*****************************************************
        // Update kategorie
        //*****************************************************
        [HttpPost("UpdateCategory/{id}")]
        [Authorize(Roles = RolesDef.ShopAdmin)]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateCategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Názov kategórie nemôže byť prázdny");

            var category = await dbContext.ShopCategories.FirstOrDefaultAsync(x => x.Id == id);

            if (category == null)
                return NotFound("Kategória neexistuje");

            category.Name = request.Name;

            await dbContext.SaveChangesAsync();

            return Ok(new { id = category.Id, name = category.Name });
        }



        //*****************************************************
        // Vytvorenie objednavky
        //*****************************************************
        [HttpPost("CreateOrder")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            var zakaznik = await dbContext.EmployeeCards
               .Include(e => e.Department)
               .FirstOrDefaultAsync(e => e.Id == request.Zakaznik);

            if (zakaznik == null)
                return BadRequest("EmployeeCard neexistuje.");


            var productIds = request.Produkty.Select(p => p.ProductId).ToList();

            var products = await dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count != request.Produkty.Count)
                return BadRequest("Niektoré produkty neexistujú");

            //vytvorenie položiek objednávky
            var orderItems = new List<OrderItem>();
            int totalPoints = 0;

            foreach (var item in request.Produkty)
            {
                var product = products.First(p => p.Id == item.ProductId);

                orderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = item.Mnozstvo,
                    Price = product.Price // snapshot ceny
                });

                totalPoints += product.Price * item.Mnozstvo;
            }

            //kontrola bodov
            if (zakaznik.PointsBalance < totalPoints)
                return BadRequest("Nedostatok bodov");


            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}";

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CisloObjednavky = orderNumber,
                Ulica = request.Ulica,
                CisloDomu = request.CisloDomu,
                City = request.City,
                PSC = request.PSC,
                Telefon = request.Telefon,
                Poznamka = request.Poznamka,
                Zakaznik = zakaznik,
                Produkty = orderItems,
                Cena = totalPoints
            };

            //odpocitanie bodov a pridanie do historie
            zakaznik.PointsBalance -= totalPoints;

            dbContext.PointsTransactions.Add(new PointsTransaction
            {
                Id = Guid.NewGuid(),
                EmployeeCardId = zakaznik.Id,
                Points = totalPoints,
                Type = "Nákup",
                Description = $"Objednávka: {orderNumber}",
                CreatedAt = DateTime.UtcNow,
                RecognitionId = order.Id
            });

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            return Ok(new { orderId = order.Id, orderNumber });
        }



        //******************************
        // Získanie objednavok
        //******************************
        [HttpGet("GetMyOrders/{id}")]
        public async Task<IActionResult> GetMyOrders(Guid id)
        {
            var orders = await dbContext.Orders
                .Where(o => o.Zakaznik.Id == id)
                .OrderByDescending(o => o.DateIn)
                .Select(o => new OrderRequest
                {
                    Id = o.Id,
                    CisloObjednavky = o.CisloObjednavky,
                    Ulica = o.Ulica,
                    CisloDomu = o.CisloDomu,
                    City = o.City,
                    PSC = o.PSC,
                    Telefon = o.Telefon,
                    Poznamka = o.Poznamka,
                    Cena = o.Cena,
                    DateIn = o.DateIn,
                    Stav = o.Stav,

                    Produkty = o.Produkty.Select(p => new Product
                    {
                        Id = p.ProductId,
                        Price = p.Price
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

    }


    public class OrderRequest
    {
        public Guid Id { get; set; }
        public string CisloObjednavky { get; set; } = default!;
        public required string Ulica { get; set; }
        public required int CisloDomu { get; set; }
        public required string City { get; set; }
        public required string PSC { get; set; }
        public required string Telefon { get; set; }
        public string? Poznamka { get; set; }
        public int Cena { get; set; }
        public ICollection<Product>? Produkty { get; set; } = new List<Product>();
        public DateTime DateIn { get; set; } = DateTime.UtcNow;
        public EnumOrderState Stav { get; set; } = EnumOrderState.Vytvorena;
    }

    public class ProductWithFileRequest
    {
        public Guid? id { get; set; }
        public string name { get; set; }
        public string info { get; set; }
        public int price { get; set; }
        public Guid shopCategoryId { get; set; }
        public IFormFile? file { get; set; }
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; }
    }

    public class CreateOrderRequest
    {
        public Guid Zakaznik { get; set; }
        public string Ulica { get; set; } = default!;
        public int CisloDomu { get; set; }
        public string City { get; set; } = default!;
        public string PSC { get; set; } = default!;
        public string Telefon { get; set; } = default!;
        public string? Poznamka { get; set; }

        public List<OrderItemRequest> Produkty { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Mnozstvo { get; set; }
    }

}

