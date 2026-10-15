using System.Reflection;
using Xunit;
var assemblies = new[] { typeof(HRMS.UnitTests.BusinessRulesTests).Assembly, typeof(HRMS.ArchitectureTests.BoundaryTests).Assembly, typeof(HRMS.IntegrationTests.HttpTests).Assembly };
int passed=0,failed=0,skipped=0;
foreach(var assembly in assemblies)
foreach(var type in assembly.GetTypes().Where(t=>t.IsClass && t.GetMethods().Any(m=>m.GetCustomAttribute<FactAttribute>() is not null)))
{
    var fixtures = new Dictionary<Type,object>();
    foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttribute<FactAttribute>() is not null))
    {
        var fact=method.GetCustomAttribute<FactAttribute>()!;
        var rows=method.GetCustomAttributes<InlineDataAttribute>().SelectMany(a=>a.GetData(method)).ToList();
        if(rows.Count==0)rows.Add([]);
        foreach(var row in rows)
        {
            var name=$"{type.Name}.{method.Name}({string.Join(",",row.Select(x=>x?.ToString()))})";
            if(fact.Skip is not null){Console.WriteLine($"SKIP {name}: {fact.Skip}");skipped++;continue;}
            object? instance=null;
            try
            {
                var constructor=type.GetConstructors().Single();
                var arguments=constructor.GetParameters().Select(p=>{if(!fixtures.TryGetValue(p.ParameterType,out var fixture)){fixture=Activator.CreateInstance(p.ParameterType)!;fixtures.Add(p.ParameterType,fixture);}return fixture;}).ToArray();
                instance=constructor.Invoke(arguments);
                var result=method.Invoke(instance,row);
                if(result is Task task) await task;
                Console.WriteLine("PASS "+name);passed++;
            }
            catch(Exception e){failed++;Console.WriteLine("FAIL "+name+"\n"+(e is TargetInvocationException ? e.InnerException : e));}
            finally{if(instance is IDisposable disposable)disposable.Dispose();}
        }
    }
    foreach(var f in fixtures.Values)if(f is IDisposable d)d.Dispose();
}
Console.WriteLine($"RESULT passed={passed} failed={failed} skipped={skipped}");
return failed==0?0:1;
