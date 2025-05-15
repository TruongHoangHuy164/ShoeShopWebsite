namespace ShoeShopWebsite.Models
{
    public interface IMessageRepository
    {
        Task SaveMessageAsync(Message message);
        Task<List<Message>> GetUnreadMessagesAsync(string userId);
        Task UpdateMessageAsync(Message message);
    }
}
