namespace Crawlers.Models;

public class BookInfo
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Category { get; set; }
    public string Author { get; set; }
    public string Dynasty { get; set; }
    public string Year { get; set; }
}

public class Book
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string Dynasty { get; set; }
    public string Year { get; set; }
    public string Category { get; set; }
    public string Url { get; set; }
    public List<BookChapter> Chapters { get; set; }
    public DateTime CreatedTime { get; set; }

    public Book()
    {
        Id = Guid.NewGuid().ToString();
        CreatedTime = DateTime.Now;
        Chapters = new List<BookChapter>();
    }
}

public class BookChapter
{
    public string Title { get; set; }
    public string Content { get; set; }
    public List<BookSection> Sections { get; set; }

    public BookChapter()
    {
        Sections = new List<BookSection>();
    }
}

public class BookSection
{
    public string Title { get; set; }
    public string Content { get; set; }
}