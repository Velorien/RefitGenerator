using Microsoft.AspNetCore.Mvc;
using RefitGenerator.TestApi.Models;
using System.Collections.Generic;

namespace RefitGenerator.TestApi.Controllers
{
    [ApiController, Route("[controller]")]
    public class DictionaryTestController : ControllerBase
    {
        [HttpPost("stringDictionary", Name = "stringDictionary")]
        public ActionResult<Dictionary<string, string>> Post(Dictionary<string, string> dictionary) => Ok(dictionary);

        [HttpPost("intDictionary", Name = "intDictionary")]
        public ActionResult<Dictionary<string, int>> Post(Dictionary<string, int> dictionary) => Ok(dictionary);

        [HttpPost("numbersDictionary", Name = "numbersDictionary")]
        public ActionResult<Dictionary<string, NumericTestModel>> Post(Dictionary<string, NumericTestModel> dictionary) => Ok(dictionary);

        [HttpPost("objectsDictionary", Name = "objectsDictionary")]
        public ActionResult<Dictionary<string, object>> Post(Dictionary<string, object> dictionary) => Ok(dictionary);

        [HttpPost("nestedDictionary", Name = "nestedDictionary")]
        public ActionResult<Dictionary<string, Dictionary<string, string>>> Post(Dictionary<string, Dictionary<string, string>> dictionary) => Ok(dictionary);
    }
}
