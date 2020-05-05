using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TaxonomyServicePOC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TaxonomyController : ControllerBase
    {
        private readonly IDatabase _db;
        private readonly ILogger<TaxonomyController> _logger;

        public TaxonomyController(IDatabase db, ILogger<TaxonomyController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("{taxonomyName}")]
        public IEnumerable<Taxonomy> GetByParentIdOrAll(string taxonomyName, [FromQuery] string parentId)
        {
            _logger.LogInformation($"{nameof(GetByParentIdOrAll)} called - TaxonomyName: {taxonomyName}");
            if (string.IsNullOrWhiteSpace(parentId))
                return _db.GetAll(taxonomyName);
            return _db.GetByParentId(taxonomyName, parentId);
        }

        [HttpGet("{taxonomyName}/{id}")]
        public Taxonomy GetById(string taxonomyName, string id)
        {
            _logger.LogInformation($"{nameof(GetById)} called - TaxonomyName: {taxonomyName}");
            return _db.GetById(taxonomyName, id);
        }

        [HttpPost("{taxonomyName}")]
        public IActionResult Create(string taxonomyName, Taxonomy obj)
        {
            _logger.LogInformation($"{nameof(Create)} called - TaxonomyName: {taxonomyName}");
            _db.InsertOrReplace(taxonomyName, obj);
            Indexer.IndexTaxonomy(taxonomyName, obj);

            return Ok();
        }


    }
}
