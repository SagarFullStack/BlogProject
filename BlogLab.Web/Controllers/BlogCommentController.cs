
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlogLab.Models.BlogComment;
using BlogLab.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogLab.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogCommentController : ControllerBase
    {
        private readonly IBlogCommentRepository _blogCommentRepository;

        public BlogCommentController(
            IBlogCommentRepository blogCommentRepository)
        {
            _blogCommentRepository = blogCommentRepository;
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
        public async Task<ActionResult<BlogComment>> Create(
            BlogCommentCreate blogCommentCreate)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized(
                    "User ID claim is missing or invalid in the token.");
            }

            var createdBlogComment =
                await _blogCommentRepository.UpsertAsync(
                    blogCommentCreate,
                    applicationUserId);

            return Ok(createdBlogComment);
        }

        [HttpGet("{blogId}")]
        public async Task<ActionResult<List<BlogComment>>> GetAll(int blogId)
        {
            var blogComments =
                await _blogCommentRepository.GetAllAsync(blogId);

            return Ok(blogComments);
        }

        [Authorize]
        [HttpDelete("{blogCommentId}")]
        public async Task<ActionResult<int>> Delete(int blogCommentId)
        {
            if (!TryGetApplicationUserId(out int applicationUserId))
            {
                return Unauthorized(
                    "User ID claim is missing or invalid in the token.");
            }

            var foundBlogComment =
                await _blogCommentRepository.GetAsync(blogCommentId);

            if (foundBlogComment == null)
            {
                return NotFound("Comment does not exist.");
            }

            if (foundBlogComment.ApplicationUserId != applicationUserId)
            {
                return Forbid();
            }

            var affectedRows =
                await _blogCommentRepository.DeleteAsync(blogCommentId);

            return Ok(affectedRows);
        }
    }
}
