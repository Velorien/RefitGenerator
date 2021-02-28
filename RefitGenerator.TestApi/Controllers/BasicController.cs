using Microsoft.AspNetCore.Mvc;
using RefitGenerator.TestApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RefitGenerator.TestApi.Controllers
{
    [ApiController, Route("[controller]")]
    public class BasicController : ControllerBase
    {
        [HttpGet(Name = "basicGet")]
        public ActionResult Get() => Ok();

        [HttpGet("deprecated", Name = "deprecatedGet"), Obsolete]
        public ActionResult DeprecatedGet() => Ok();

        [HttpGet("{id}", Name = "basicGetWithRouteParam")]
        public ActionResult<int> Get(int id) => Ok(id);

        [HttpGet("withQuery", Name = "basicGetWitQueryParam")]
        public ActionResult<string> Get(string queryParam) => Ok(queryParam);

        [HttpGet("withComplexQuery", Name = "basicGetWithComplexQueryParam")]
        public ActionResult<NumericTestModel> Get([FromQuery] NumericTestModel model) => Ok(model);

        [HttpPost(Name = "emptyPost")]
        public ActionResult Post() => Ok();

        [HttpPost("numbers", Name = "postNumbers")]
        public ActionResult<NumericTestModel> Post(NumericTestModel numbers) => Ok(numbers);
    }
}
