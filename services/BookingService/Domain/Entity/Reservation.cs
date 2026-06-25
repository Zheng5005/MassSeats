public class Reservation
{
    public int id { get; set; }

    public int user_id { get; set; 

    public int event_id { get; set; }

    public int seat_number { get; set; }

    public string seat_section { get; set; }
    
    public int seat_row { get; set; }

    public float price { get; set; }

    public string status { get; set; }

    public int? payment_id { get; set; }

    public DateTimeOffset reserved_at { get; set; }
}
