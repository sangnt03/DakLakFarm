using System;
using System.Linq;
using System.Text;

namespace AgriEcommerces_MVC.Helpers
{
    public static class OrderCodeGenerator
    {
        // Mã ngày + ID (Vd: AG20251116-00123)
        public static string GenerateOrderCode_DateId(int orderId, DateTime orderDate)
        {
            return $"AG{orderDate:yyyyMMdd}-{orderId:D5}";
            // Output: AG20251116-00123
        }
    }
}