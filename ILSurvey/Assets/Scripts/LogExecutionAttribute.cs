using System;
using UnityEngine;

namespace XPlan.ILWeave
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class LogExecutionAttribute : Attribute
    {
    }
}
