using System.ComponentModel.DataAnnotations;

namespace SmartArchivist.Infrastructure.ElasticSearch
{
    /// <summary>
    /// Configuration settings for Elasticsearch connection and indexing.
    /// </summary>
    public class ElasticSearchConfig
    {
        [Required]
        [Url]
        public required string Url { get; set; }

        [Required]
        [MinLength(1)]
        public required string IndexName { get; set; }

        [Required]
        public required int MaxSearchResults { get; set; }
    }
}