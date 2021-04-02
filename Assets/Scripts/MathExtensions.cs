using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    partial class MathExtension
    {
        public static int Clamp(int number, int low, int high)
        {
            if (number < low)
            {
                return low;
            }
            if (number > high)
            {
                return high;
            }
            return number;
        }
    }
