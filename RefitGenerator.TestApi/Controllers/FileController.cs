using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RefitGenerator.TestApi.Controllers
{
    [ApiController, Route("[controller]")]
    public class FileController : ControllerBase
    {
        [HttpGet(Name = "downloadFile"), Produces(typeof(FileResult))]
        public FileResult Download()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello world!"));
            return new FileStreamResult(stream, "text/plain") { FileDownloadName = "hello.txt" };
        }

        [HttpPost("uploadSingle", Name = "uploadSingleFile")]
        public ActionResult<long> UploadSingle(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return Ok(stream.Length);
        }

        [HttpPost("uploadMany", Name = "uploadManyFiles")]
        public ActionResult<List<long>> UploadMany(List<IFormFile> files)
        {
            List<long> result = new List<long>();
            foreach(var file in files)
            {
                using var stream = file.OpenReadStream();
                result.Add(stream.Length);
            }

            return Ok(result);
        }
    }
}
