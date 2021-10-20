using System.Collections.Generic;
using System.Linq;

namespace SyntropyNet.WindowsApp.Application.Domain.Helpers {
    public static class IEnumerableHelpers {
        /// <summary>
        /// Will return false if collection is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static bool NotEmpty<T>(this IEnumerable<T> collection) {
            return collection != null && collection.Any();
        }

        /// <summary>
        /// Will return true if collection is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static bool IsEmpty<T>(this IEnumerable<T> collection) {
            return !NotEmpty(collection);
        }
    }
}
