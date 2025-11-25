using Mono.Cecil;
using UnityEngine;

namespace XPlan.Weaver.Abstractions
{
    /****************************************
    * 切面介面：所有級別的 Aspect 都實作它們
    ****************************************/
    public interface IMethodAspectWeaver
    {
        string AttributeFullName { get; }

        void Apply(ModuleDefinition module, MethodDefinition targetMethod, CustomAttribute attr);
    }

    public interface ITypeAspectWeaver
    {
        string AttributeFullName { get; }
        void Apply(ModuleDefinition module, TypeDefinition targetType, CustomAttribute attr);
    }

    public interface IFieldAspectWeaver
    {
        string AttributeFullName { get; }
        void Apply(ModuleDefinition module, FieldDefinition targetField, CustomAttribute attr);
    }
}
