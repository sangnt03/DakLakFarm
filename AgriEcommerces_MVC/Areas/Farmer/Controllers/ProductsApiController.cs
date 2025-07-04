using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Areas.Farmer.ApiModels;
using AgriEcommerces_MVC.Areas.Farmer.ViewModels;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Route("api/farmer/products")]
    [ApiController]
    [Authorize(Roles = "Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class ProductsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public ProductsApiController(ApplicationDbContext db) => _db = db;
        private int FarmerId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        // GET api/farmer/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
        {
            var list = await _db.products
                .Where(p => p.userid == FarmerId)
                .Include(p => p.category)
                .Include(p => p.productimages)
                .Select(p => new ProductDto
                {
                    ProductId = p.productid,
                    UserId = p.userid,
                    CategoryId = p.categoryid,
                    CategoryName = p.category.categoryname,
                    ProductName = p.productname,
                    Description = p.description,
                    Unit = p.unit,
                    Price = p.price,
                    QuantityAvailable = p.quantityavailable,
                    CreatedAt = p.createdat ?? DateTime.MinValue,
                    ImageUrls = p.productimages
                                .OrderBy(pi => pi.uploadedat)
                                .Select(pi => pi.imageurl)
                                .ToList(),
                    ImageIds = p.productimages
                                .OrderBy(pi => pi.uploadedat)
                                .Select(pi => pi.imageid)
                                .ToList()
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET api/farmer/products/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> Get(int id)
        {
            var dto = await _db.products
                .Where(p => p.productid == id && p.userid == FarmerId)
                .Include(p => p.category)
                .Include(p => p.productimages)
                .Select(p => new ProductDto
                {
                    ProductId = p.productid,
                    UserId = p.userid,
                    CategoryId = p.categoryid,
                    CategoryName = p.category.categoryname,
                    ProductName = p.productname,
                    Description = p.description,
                    Unit = p.unit,
                    Price = p.price,
                    QuantityAvailable = p.quantityavailable,
                    CreatedAt = p.createdat ?? DateTime.MinValue,
                    ImageUrls = p.productimages
                                .OrderBy(pi => pi.uploadedat)
                                .Select(pi => pi.imageurl)
                                .ToList(),
                    ImageIds = p.productimages
                                .OrderBy(pi => pi.uploadedat)
                                .Select(pi => pi.imageid)
                                .ToList()
                })
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();
            return Ok(dto);
        }

        // POST api/farmer/products
        [HttpPost]
        public async Task<ActionResult<ProductDto>> Create([FromForm] ProductCreateFormDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { Error = "Dữ liệu không hợp lệ", Details = ModelState });

                var category = await _db.categories.FindAsync(model.CategoryId);
                if (category == null)
                    return BadRequest(new { Error = "Danh mục không tồn tại" });

                if (model.ProductImages == null || !model.ProductImages.Any())
                    return BadRequest(new { Error = "Vui lòng chọn ít nhất một hình ảnh" });

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                const long maxFileSize = 5 * 1024 * 1024;
                foreach (var file in model.ProductImages)
                {
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                        return BadRequest(new { Error = $"Định dạng file {file.FileName} không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}" });
                    if (file.Length > maxFileSize)
                        return BadRequest(new { Error = $"Kích thước file {file.FileName} vượt quá giới hạn 5MB" });
                }

                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var entity = new product
                {
                    userid = FarmerId,
                    categoryid = model.CategoryId,
                    productname = model.ProductName,
                    description = model.Description,
                    unit = model.Unit,
                    price = model.Price,
                    quantityavailable = model.QuantityAvailable,
                    createdat = DateTime.Now
                };
                _db.products.Add(entity);
                await _db.SaveChangesAsync();

                var imageUrls = new List<string>();
                var imageIds = new List<int>();
                foreach (var file in model.ProductImages)
                {
                    var fname = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var path = Path.Combine(uploadDir, fname);
                    using var stream = System.IO.File.Create(path);
                    await file.CopyToAsync(stream);

                    var imageUrl = "/uploads/" + fname;
                    var productImage = new productimage
                    {
                        productid = entity.productid,
                        imageurl = imageUrl,
                        uploadedat = DateTime.Now
                    };
                    _db.productimages.Add(productImage);
                    await _db.SaveChangesAsync(); // Lưu để lấy ImageId

                    imageUrls.Add(imageUrl);
                    imageIds.Add(productImage.imageid);
                }

                var dto = new ProductDto
                {
                    ProductId = entity.productid,
                    UserId = entity.userid,
                    CategoryId = entity.categoryid,
                    CategoryName = category.categoryname,
                    ProductName = entity.productname,
                    Description = entity.description,
                    Unit = entity.unit,
                    Price = entity.price,
                    QuantityAvailable = entity.quantityavailable,
                    CreatedAt = entity.createdat ?? DateTime.MinValue,
                    ImageUrls = imageUrls,
                    ImageIds = imageIds
                };
                return CreatedAtAction(nameof(Get), new { id = dto.ProductId }, dto);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                return BadRequest(new
                {
                    Error = "Lỗi cơ sở dữ liệu: " + pgEx.Message,
                    Detail = pgEx.Detail ?? "Không có chi tiết lỗi",
                    SqlState = pgEx.SqlState
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Error = "Lỗi không xác định: " + ex.Message,
                    Detail = ex.StackTrace
                });
            }
        }

        // PUT api/farmer/products/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] ProductEditViewModel model)
        {
            try
            {
                // 1) Validate model fields
                if (!ModelState.IsValid)
                    return BadRequest(new { Error = "Dữ liệu không hợp lệ", Details = ModelState });

                // 2) Lấy sản phẩm và kiểm tra quyền
                var entity = await _db.products.FindAsync(id);
                if (entity == null || entity.userid != FarmerId)
                    return NotFound(new { Error = "Không tìm thấy sản phẩm hoặc không có quyền truy cập" });

                // 3) Kiểm tra danh mục tồn tại
                var category = await _db.categories.FindAsync(model.CategoryId);
                if (category == null)
                    return BadRequest(new { Error = "Danh mục không tồn tại" });

                // 4) Cập nhật các thông tin cơ bản
                entity.categoryid = model.CategoryId;
                entity.productname = model.ProductName;
                entity.description = model.Description;
                entity.unit = model.Unit;
                entity.price = model.Price;
                entity.quantityavailable = model.QuantityAvailable;

                // 5) Xử lý ảnh: nếu có file mới -> xóa ảnh cũ và thêm ảnh mới
                var files = Request.Form.Files.Where(f => f.Name == "ProductImages").ToList();
                if (files != null && files.Count > 0)
                {
                    // Validate file extensions và sizes
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    const long maxFileSize = 5 * 1024 * 1024;

                    foreach (var file in files)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                            return BadRequest(new { Error = $"Định dạng file {file.FileName} không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}" });
                        if (file.Length > maxFileSize)
                            return BadRequest(new { Error = $"Kích thước file {file.FileName} vượt quá giới hạn 5MB" });
                    }

                    // a) Xóa bản ghi và file ảnh cũ
                    var oldImages = await _db.productimages
                        .Where(pi => pi.productid == id)
                        .ToListAsync();

                    foreach (var img in oldImages)
                    {
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.imageurl.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.IO.File.Delete(filePath);
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue
                                Console.WriteLine($"Cannot delete file {filePath}: {ex.Message}");
                            }
                        }
                    }
                    _db.productimages.RemoveRange(oldImages);

                    // b) Thêm ảnh mới
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    foreach (var file in files)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var fname = $"{Guid.NewGuid()}{ext}";
                        var savePath = Path.Combine(uploadDir, fname);
                        using (var stream = System.IO.File.Create(savePath))
                            await file.CopyToAsync(stream);

                        _db.productimages.Add(new productimage
                        {
                            productid = entity.productid,
                            imageurl = "/uploads/" + fname,
                            uploadedat = DateTime.Now
                        });
                    }
                }

                await _db.SaveChangesAsync();
                return Ok(new { Message = "Cập nhật sản phẩm thành công" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                return BadRequest(new
                {
                    Error = "Lỗi cơ sở dữ liệu: " + pgEx.Message,
                    Detail = pgEx.Detail ?? "Không có chi tiết lỗi",
                    SqlState = pgEx.SqlState
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Error = "Lỗi không xác định: " + ex.Message,
                    Detail = ex.StackTrace
                });
            }
        }

        // DELETE api/farmer/products/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var entity = await _db.products
                    .Include(p => p.productimages)
                    .FirstOrDefaultAsync(p => p.productid == id && p.userid == FarmerId);

                if (entity == null)
                    return NotFound(new { Error = "Không tìm thấy sản phẩm hoặc không có quyền truy cập" });

                // Xóa file ảnh trước
                foreach (var img in entity.productimages)
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.imageurl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue
                            Console.WriteLine($"Cannot delete file {filePath}: {ex.Message}");
                        }
                    }
                }

                // Xóa bản ghi (cascade sẽ xóa productimages)
                _db.products.Remove(entity);
                await _db.SaveChangesAsync();

                return Ok(new { Message = "Xóa sản phẩm thành công" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                return BadRequest(new
                {
                    Error = "Lỗi cơ sở dữ liệu: " + pgEx.Message,
                    Detail = pgEx.Detail ?? "Không có chi tiết lỗi",
                    SqlState = pgEx.SqlState
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Error = "Lỗi không xác định: " + ex.Message,
                    Detail = ex.StackTrace
                });
            }
        }
    }
}