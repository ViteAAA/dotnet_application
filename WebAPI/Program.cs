using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Printing_slip;

class Program
{
    private static async Task Main()
    {
        // URL вашего локального сайта в OSPanel (без косой черты на конце)
        const string siteUrl = "http://test-1-example.local";
        const string ck = "ck_5a3ecec64823b34b51571946dd2cb598a591b6b6";
        const string cs = "cs_f22afedef524e44fd36489bac793384f41b9489d";

        // ИСПРАВЛЕНО: Добавлен обязательный роут к заказам WooCommerce REST API (/wp-json/wc/v3/orders)
        const string requestUrl = $"{siteUrl}/wp-json/wc/v3/orders?status=processing&consumer_key={ck}&consumer_secret={cs}";

        using var client = new HttpClient();
        try
        {
            var response = await client.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                    
                // Теперь сюда придет чистый массив JSON с вашими заказами в статусе "В обработке"
                Console.WriteLine(jsonResponse);
            }
            else
            {
                // Поможет отладить, если WooCommerce вернет ошибку (например, 401 Unauthorized)
                Console.WriteLine($"Сервер вернул ошибку: {response.StatusCode} ({(int)response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка связи с OSPanel: {ex.Message}");
        }
    }
}