using System;
using System.Net.Http;
using System.Threading.Tasks;

using {0}.Models;

namespace {0}
{{
    class Program
    {{
        static async Task Main(string[] args)
        {{
            var http = new HttpClient();
            http.BaseAddress = new Uri("{1}");
            var apiClient = new ApiClient(http);
        }}
    }}
}}
