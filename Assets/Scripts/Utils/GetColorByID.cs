using UnityEngine;

namespace Utils {

    public class ColorTools {

        public static Color GetColorByID(int id)
        {
            switch (id)
            {
                case 1:
                    return Color.red;
                case 2:
                    return Color.blue;
                case 3:
                    return Color.yellow;
                case 4:
                    return Color.green;
                default:
                    break;
            }

            return Color.white;
        }
    }
}
