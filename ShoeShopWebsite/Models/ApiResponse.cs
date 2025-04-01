namespace ShoeShopWebsite.Models
{
    public class ApiResponse<T>
    {
        public int Error { get; set; }
        public string Msg { get; set; }
        public List<T> Data { get; set; }
    }
}
