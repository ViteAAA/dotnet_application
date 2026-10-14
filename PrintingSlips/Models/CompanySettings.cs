namespace PrintingSlips.Models;

public class CompanySettings
{
    public string SellerTitle { get; set; } = "Max Food ---------- ИП Мельник";
    public string SellerName { get; set; } = "Сергей Викторович";
    public string Inn { get; set; } = "615433318557";
    public string Manager { get; set; } = "MaxFood";
    public string SalesChannel { get; set; } = "Сайт";
    public string SignReleased { get; set; } = "Груз сдал: --------------";
    public string SignReceived { get; set; } = "Груз принял: ------------";
    public string Footer { get; set; } = "СПАСИБО! ЖДЕМ ВАС СНОВА!";
    public string CurrencyNote { get; set; } = "ВСЕ СУММЫ В РУБЛЯХ";
}
