using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace FM39hz.DataCatalyst.Plugins.Modding.Weaver;

public sealed class HookInjector {
    private readonly WeaverConfig _config;
    private readonly AssemblyDefinition _hookRuntime;

    private MethodReference _beforeMethod = null!;
    private MethodReference _afterMethod = null!;

    public HookInjector(WeaverConfig config, AssemblyDefinition hookRuntime) {
        _config = config;
        _hookRuntime = hookRuntime;
    }

    public void Initialize() {
        var dispatcherType = _hookRuntime.MainModule
            .GetType("FM39hz.DataCatalyst.Plugins.Modding.Runtime.HookDispatcher");

        if (dispatcherType is null)
            throw new InvalidOperationException("Cannot find HookDispatcher in Modding.Runtime");

        _beforeMethod = _hookRuntime.MainModule.ImportReference(
            dispatcherType.Methods.Single(m => m.Name == "Before"));

        _afterMethod = _hookRuntime.MainModule.ImportReference(
            dispatcherType.Methods.Single(m => m.Name == "After"));
    }

    public int Inject(AssemblyDefinition targetAssembly) {
        var count = 0;
        var module = targetAssembly.MainModule;
        var before = module.ImportReference(_beforeMethod);
        var after = module.ImportReference(_afterMethod);
        var objectType = module.ImportReference(typeof(object));
        var objectArrayType = module.ImportReference(typeof(object[]));

        foreach (var type in module.GetAllTypes()) {
            foreach (var method in type.Methods) {
                if (!MethodFilter.ShouldInject(method, _config))
                    continue;

                InjectMethod(method, before, after, objectType, objectArrayType);
                count++;
            }
        }

        return count;
    }

    private static void InjectMethod(
        MethodDefinition method,
        MethodReference before,
        MethodReference after,
        TypeReference objectType,
        TypeReference objectArrayType) {

        var il = method.Body.GetILProcessor();
        var hookId = MethodFilter.BuildHookId(method);
        var isVoid = method.ReturnType.FullName == "System.Void";
        var hasReturn = !isVoid;

        // ── Before injection ──
        var first = method.Body.Instructions[0];

        // Build object[] args
        var paramCount = method.Parameters.Count;
        il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, paramCount));
        il.InsertBefore(first, il.Create(OpCodes.Newarr, objectType));

        for (var i = 0; i < paramCount; i++) {
            il.InsertBefore(first, il.Create(OpCodes.Dup));
            il.InsertBefore(first, il.Create(OpCodes.Ldc_I4, i));
            il.InsertBefore(first, il.Create(OpCodes.Ldarg, method.Parameters[i]));
            var pt = method.Parameters[i].ParameterType;
            if (pt.IsValueType && pt.FullName != "System.Boolean"
                               && pt.FullName != "System.Int32"
                               && pt.FullName != "System.Single"
                               && pt.FullName != "System.Double"
                               && pt.FullName != "System.Int64")
                il.InsertBefore(first, il.Create(OpCodes.Box, pt));
            il.InsertBefore(first, il.Create(OpCodes.Stelem_Ref));
        }

        var argsLocal = new VariableDefinition(objectArrayType);
        method.Body.Variables.Add(argsLocal);
        il.InsertBefore(first, il.Create(OpCodes.Stloc, argsLocal));

        // out object? returnValue local
        var retLocal = new VariableDefinition(objectType);
        method.Body.Variables.Add(retLocal);

        // Before(methodId, instance, args, out retLocal)
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, hookId));
        il.InsertBefore(first, method.IsStatic
            ? il.Create(OpCodes.Ldnull)
            : il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldloc, argsLocal));
        il.InsertBefore(first, il.Create(OpCodes.Ldloca, retLocal));
        il.InsertBefore(first, il.Create(OpCodes.Call, before));

        var skipTarget = il.Create(OpCodes.Nop);
        il.InsertBefore(first, il.Create(OpCodes.Brfalse, skipTarget));

        // Skip path: if Before returned true → return retLocal (unboxed)
        if (hasReturn) {
            il.InsertBefore(first, il.Create(OpCodes.Ldloc, retLocal));
            il.InsertBefore(first, il.Create(OpCodes.Unbox_Any, method.ReturnType));
        }
        il.InsertBefore(first, il.Create(OpCodes.Ret));

        // ── After injection — before each Ret ──
        if (hasReturn) {
            var returnLocal = new VariableDefinition(method.ReturnType);
            method.Body.Variables.Add(returnLocal);

            foreach (var ret in method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ret).ToList()) {

                // stloc returnLocal (save return value)
                il.InsertBefore(ret, il.Create(OpCodes.Stloc, returnLocal));

                // Box return value → call After
                il.InsertBefore(ret, il.Create(OpCodes.Ldstr, hookId));
                il.InsertBefore(ret, method.IsStatic
                    ? il.Create(OpCodes.Ldnull)
                    : il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Ldloc, returnLocal));
                il.InsertBefore(ret, il.Create(OpCodes.Box, method.ReturnType));
                il.InsertBefore(ret, il.Create(OpCodes.Call, after));

                // Restore return value
                il.InsertBefore(ret, il.Create(OpCodes.Ldloc, returnLocal));
            }
        } else {
            // Void: call After with null
            foreach (var ret in method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ret).ToList()) {

                il.InsertBefore(ret, il.Create(OpCodes.Ldstr, hookId));
                il.InsertBefore(ret, method.IsStatic
                    ? il.Create(OpCodes.Ldnull)
                    : il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ret, il.Create(OpCodes.Ldnull));
                il.InsertBefore(ret, il.Create(OpCodes.Call, after));
            }
        }
    }
}
