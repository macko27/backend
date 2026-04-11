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
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await dbContext.Products
                .Include(p => p.ShopCategory)
                .Include(p => p.ProductAttachment)
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
                .Where(p => p.Id == id)
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
                .Include(p => p.ProductAttachment)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound("Produkt nebol nájdený");

            // Odstránenie attachmentu zo storage, ak existuje
            if (product.ProductAttachment != null)
            {
                try
                {
                    BlobServiceClient client = new(config.GetSection("Blob")["BlobConnect"]);
                    var container = client.GetBlobContainerClient("product");
                    var blobClient = container.GetBlobClient(new Uri(product.ProductAttachment.FileUrl).Segments.Last());
                    await blobClient.DeleteIfExistsAsync();

                    dbContext.ProductAttachments.Remove(product.ProductAttachment);
                }
                catch
                {
                    // Môžeš logovať chybu, ale necháme pokračovať
                }
            }

            dbContext.Products.Remove(product);
            await dbContext.SaveChangesAsync();

            return Ok("Produkt bol odstránený");
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

}

