using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RefitGenerator.TestApi.Controllers
{
    [ApiController, Route("[controller]")]
    public class FormController : ControllerBase
    {
        [HttpPost("formDictionary", Name = "postFormDictionary")]
        public ActionResult<Dictionary<string, string>> Post([FromForm] Dictionary<string, string> formContent) => Ok(formContent);

        [HttpPost("formParameters", Name = "postFormParameters")]
        public ActionResult<string> Post([FromForm] string text, [FromForm] int number) => Ok($"{text}-{number}");
    }
}
