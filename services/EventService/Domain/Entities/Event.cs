public class Event
{
    public int id { get; set; }

    public string title { get; set; 

    public string? description { get; set; }

    public int category_id { get; set; }

    public int venue_id { get; set; }

    public DateTimeOffset Event_date { get; set; }

    public float ticket_price { get; set; } // Future improvement: support multiples types, VIP, Platinum, General

    public int total_seats { get; set; }

    public int available_seats { get; set; }

    public string? banner_image { get; set; }

    public DateTimeOffset created_at { get; set; }

    public DateTimeOffset updated_at { get; set; }
}
