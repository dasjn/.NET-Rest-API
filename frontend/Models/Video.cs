using System.ComponentModel.DataAnnotations;

namespace IA.FrontEnd.Models
{
    public class Video
    {
        public long Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime PublishDate { get; set; }
        public required String Uri { get; set; }
    }
}
