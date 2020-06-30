﻿using System.Security.Cryptography;
using System.Text;

namespace Sisfarma.Sincronizador.Helpers
{
    public static class Cryptographer
    {
        public static string GenerateMd5Hash(this string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

                var builder = new StringBuilder();
                
                for (int i = 0; i < data.Length; i++)
                    builder.Append(data[i].ToString("x2"));

                return builder.ToString();
            }
        }
    }
}
