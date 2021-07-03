﻿using System;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Common
{
    public static class CustomLogger
    {
        public static void Log(string log)
        {
            Debug.Log(log);
        }

        public static void Log(Exception ex)
        {
            Debug.Log(ex);
        }
    }
}