
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlogLab.Models.Photo;
using BlogLab.Repository;
using BlogLab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogLab.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoRepository _photoRepository;
        private readonly IBlogRepository _blogRepository;
        private readonly IPhotoService _photoService;

        public PhotoController(
            IPhotoRepository photoRepository,
            IBlogRepository blogRepository,
            IPhotoService photoService)
        {
            _photoRepository = photoRepository;
            _blogRepository = blogRepository;
            _photoService = photoService;
        }

        private bool TryGetApplicationUserId(out int applicationUserId)
        {
            applicationUserId = 0;

            var userIdClaim = User.Claims.FirstOrDefault(claim =>
                claim.Type == JwtRegisteredClaimNames.NameId ||
                claim.Type == "nameid" ||
                claim.Type == ClaimTypes.NameIdentifier);

            return userIdClaim != null &&
                   int.TryParse(userIdClaim.Value, out applicationUserId);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Photo>> UploadPhoto(
            [FromForm] Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized("User ID claim is missing or invalid.");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Please select a valid image file.");
            }

            var uploadResult = await _photoService.AddPhotoAsync(file);

            if (uploadResult == null)
            {
                return StatusCode(502, "Photo upload service returned no result.");
            }

            if (uploadResult.Error != null)
            {
                return BadRequest(uploadResult.Error.Message);
            }

            if (uploadResult.SecureUrl == null)
            {
                return StatusCode(502, "Photo upload did not return a secure URL.");
            }

            var photoCreate = new PhotoCreate
            {
                PublicId = uploadResult.PublicId,
                ImageUrl = uploadResult.SecureUrl.AbsoluteUri,
                Description = file.FileName
            };

            var photo = await _photoRepository.InsertAsync(
                photoCreate, applicationUserId);

            return Ok(photo);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<List<Photo>>> GetByApplicationUserId()
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized("User ID claim is missing or invalid.");
            }

            var photos = await _photoRepository.GetAllByUserIdAsync(
                applicationUserId);

            return Ok(photos);
        }

        [HttpGet("{photoId}")]
        public async Task<ActionResult<Photo>> Get(int photoId)
        {
            var photo = await _photoRepository.GetAsync(photoId);

            if (photo == null)
            {
                return NotFound("Photo does not exist.");
            }

            return Ok(photo);
        }

        [Authorize]
        [HttpDelete("{photoId}")]
        public async Task<ActionResult<int>> Delete(int photoId)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized("User ID claim is missing or invalid.");
            }

            var foundPhoto = await _photoRepository.GetAsync(photoId);

            if (foundPhoto == null)
            {
                return NotFound("Photo does not exist.");
            }

            if (foundPhoto.ApplicationUserId != applicationUserId)
            {
                return Forbid();
            }

            var blogs = await _blogRepository.GetAllByUserIdAsync(
                applicationUserId);

            if (blogs.Any(blog => blog.PhotoId == photoId))
            {
                return BadRequest(
                    "Cannot remove photo because it is used in published blogs.");
            }

            var deleteResult = await _photoService.DeletePhotoAsync(
                foundPhoto.PublicId);

            if (deleteResult.Error != null)
            {
                return BadRequest(deleteResult.Error.Message);
            }

            var affectedRows = await _photoRepository.DeletetAsync(
                foundPhoto.PhotoId);

            return Ok(affectedRows);
        }
    }
}
