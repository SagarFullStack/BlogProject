
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlogLab.Models.Blog;
using BlogLab.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogLab.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogController : ControllerBase
    {
        private readonly IBlogRepository _blogRepository;
        private readonly IPhotoRepository _photoRepository;

        public BlogController(
            IBlogRepository blogRepository,
            IPhotoRepository photoRepository)
        {
            _blogRepository = blogRepository;
            _photoRepository = photoRepository;
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
        public async Task<ActionResult<Blog>> Create(BlogCreate blogCreate)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized("User ID claim is missing or invalid in the token.");
            }

            if (blogCreate.PhotoId.HasValue)
            {
                var photo = await _photoRepository.GetAsync(blogCreate.PhotoId.Value);

                if (photo == null)
                {
                    return NotFound("Photo not found.");
                }

                if (photo.ApplicationUserId != applicationUserId)
                {
                    return BadRequest("You did not upload the photo.");
                }
            }

            var blog = await _blogRepository.UpsertAsync(
                blogCreate,
                applicationUserId);

            return Ok(blog);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResults<Blog>>> GetAll(
            [FromQuery] BlogPaging blogPaging)
        {
            var blogs = await _blogRepository.GetAllAsync(blogPaging);
            return Ok(blogs);
        }

        [HttpGet("{blogId}")]
        public async Task<ActionResult<Blog>> Get(int blogId)
        {
            var blog = await _blogRepository.GetAsync(blogId);

            if (blog == null)
            {
                return NotFound("Blog not found.");
            }

            return Ok(blog);
        }

        [HttpGet("user/{applicationUserId}")]
        public async Task<ActionResult<List<Blog>>> GetByApplicationUserId(
            int applicationUserId)
        {
            var blogs = await _blogRepository.GetAllByUserIdAsync(
                applicationUserId);

            return Ok(blogs);
        }

        [HttpGet("famous")]
        public async Task<ActionResult<List<Blog>>> GetAllFamous()
        {
            var blogs = await _blogRepository.GetAllFamousAsync();
            return Ok(blogs);
        }

        [Authorize]
        [HttpDelete("{blogId}")]
        public async Task<ActionResult<int>> Delete(int blogId)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized("User ID claim is missing or invalid in the token.");
            }

            var foundBlog = await _blogRepository.GetAsync(blogId);

            if (foundBlog == null)
            {
                return NotFound("Blog does not exist.");
            }

            if (foundBlog.ApplicationUserId != applicationUserId)
            {
                return Forbid();
            }

            var affectedRows = await _blogRepository.DeleteAsync(blogId);
            return Ok(affectedRows);
        }
    }
}
