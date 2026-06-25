public class Payment
{
    public int id { get; set; }

    public int booking_id { get; set; 

    public string stripe_payment_intent { get; set; }

    public float amount { get; set; }

    public string currency { get; set; }
    
    public string payment_method { get; set; }

    public string status { get; set; }

    public DateTimeOffset created_at { get; set; }

    public DateTimeOffset updated_at { get; set; }
}
