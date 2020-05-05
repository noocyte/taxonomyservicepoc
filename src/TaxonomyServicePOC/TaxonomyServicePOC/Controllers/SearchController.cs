using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaxonomyServicePOC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IDatabase _db;
        public SearchController(IDatabase db)
        {
            _db = db;
        }

        [HttpPost("{taxonomyName}")]
        public IEnumerable<Taxonomy> Search(string taxonomyName, SearchParameters q)
        {
            return Indexer.Search(taxonomyName, q);
        }
    }

    public class SearchParameters
    {
        public string Query { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }
        public int PageSize { get; set; }
    }
}
