namespace VendorLoanQuoteGenerator.DataTransfer;

public class GenerateLoanQuoteRequest
{
    public string CustomerId { get; set; }

    public string CorrelationId { get; set; }

    public string LoanAmount { get; set; }
    
    public string ReturnAddress { get; set; }
    
    public string TraceIdentifier { get; set; }
}