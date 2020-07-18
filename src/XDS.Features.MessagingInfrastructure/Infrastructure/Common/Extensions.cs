using System;
using System.Collections.Generic;
using System.Text;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common
{
    public static class Extensions
    {
        public static void NotNull<T>(ref HashSet<T> list, int capacity)
        {
            if (list == null)
                list = new HashSet<T>(capacity);
        }

        public static void NotNull<K, T>(ref Dictionary<K, T> list, int capacity)
        {
            if (list == null)
                list = new Dictionary<K, T>(capacity);
        }
    }
}
