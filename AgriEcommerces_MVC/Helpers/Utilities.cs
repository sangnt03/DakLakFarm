using System.Text;
using System.Text.RegularExpressions;

namespace AgriEcommerces_MVC.Helpers // Nhớ check namespace của bạn
{
    public static class Utilities
    {
        public static string ToSlug(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            str = str.ToLower().Trim();

            // Thay thế ký tự tiếng Việt
            string[] fromChars = new string[] { "á", "à", "ả", "ã", "ạ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "đ", "é", "è", "ẻ", "ẽ", "ẹ", "ê", "ế", "ề", "ể", "ễ", "ệ", "í", "ì", "ỉ", "ĩ", "ị", "ó", "ò", "ỏ", "õ", "ọ", "ô", "ố", "ồ", "ổ", "ỗ", "ộ", "ơ", "ớ", "ờ", "ở", "ỡ", "ợ", "ú", "ù", "ủ", "ũ", "ụ", "ư", "ứ", "ừ", "ử", "ữ", "ự", "ý", "ỳ", "ỷ", "ỹ", "ỵ" };
            string[] toChars = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "d", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "i", "i", "i", "i", "i", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "y", "y", "y", "y", "y" };

            for (int i = 0; i < fromChars.Length; i++)
            {
                str = str.Replace(fromChars[i], toChars[i]);
            }

            // Loại bỏ ký tự đặc biệt
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            // Chuyển khoảng trắng thành gạch ngang
            str = Regex.Replace(str, @"\s+", "-");
            // Loại bỏ gạch ngang dư thừa
            str = Regex.Replace(str, @"-+", "-");

            return str;
        }
    }
}