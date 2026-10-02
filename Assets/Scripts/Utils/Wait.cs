using System;
using System.Collections;
using UnityEngine;

namespace Utils {

    public static class Wait {

        /*
         * @brief Wait for a certain amount of time and then call the callback.
         */
        public static IEnumerator WaitAndCallback(float time, Action callback)
        {
            yield return new WaitForSeconds(time);

            callback?.Invoke();
        }
    }
}
