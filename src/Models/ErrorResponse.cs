namespace VeteransAffairsMCP.Models
{
    public class ErrorResponse
    {
        public string ErrorMessage { get; set; }
        public int StatusCode { get; set; }

        public ErrorResponse(string errorMessage, int statusCode)
        {
            ErrorMessage = errorMessage;
            StatusCode = statusCode;
        }
    }
}