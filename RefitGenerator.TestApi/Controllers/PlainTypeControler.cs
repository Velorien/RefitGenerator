using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RefitGenerator.TestApi.Controllers
{
    [ApiController, Route("[controller]")]
    public class PlainTypeController
    {
        [HttpGet("long", Name = "getLong")]
        public long GetLong() => 10;
    }
}
