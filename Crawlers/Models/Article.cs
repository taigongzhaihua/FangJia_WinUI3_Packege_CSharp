namespace Crawlers.Models
{
    public class Article
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string PublishDate { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; }
        public string Url { get; set; }
        public List<string> Tags { get; set; }
        public DateTime CreatedTime { get; set; }

        public Article()
        {
            Id = Guid.NewGuid().ToString();
            CreatedTime = DateTime.Now;
            Tags = new List<string>();
        }
    }

    public class ArticleStatistics
    {
        public int TotalArticles { get; set; }
        public int ArticlesWithContent { get; set; }
        public int ArticlesWithAuthor { get; set; }
        public int ArticlesWithDate { get; set; }
        public int ArticlesWithTags { get; set; }
        public Dictionary<string, int> TopTags { get; set; }

        public ArticleStatistics()
        {
            TopTags = new Dictionary<string, int>();
        }
    }
}