using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SleepRunner.Automation.Race.Policy.Training;

namespace SleepRunner.Tests.Forms;

internal static class WinFormsTestHost
{
    private static readonly Assembly AppAssembly = typeof(TrainingRuleLoader).Assembly;

    public static void Run(Action action)
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex is TargetInvocationException tie
                    ? tie.InnerException ?? tie
                    : ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    public static object CreateInternal(string fullTypeName, params object[] args)
    {
        Type type = AppAssembly.GetType(fullTypeName)
            ?? throw new InvalidOperationException($"Type '{fullTypeName}' not found.");

        return Activator.CreateInstance(
                   type,
                   BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                   binder: null,
                   args: args,
                   culture: null)
               ?? throw new InvalidOperationException($"Could not create '{fullTypeName}'.");
    }

    public static void Invoke(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on '{instance.GetType().FullName}'.");

        method.Invoke(instance, args);
    }

    public static T ReadPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on '{instance.GetType().FullName}'.");

        object? value = field.GetValue(instance);
        return value is T typed
            ? typed
            : throw new InvalidOperationException($"Field '{fieldName}' is not a '{typeof(T).Name}'.");
    }

    public static IReadOnlyList<string> CollectTexts(Control root)
    {
        var texts = new List<string>();
        CollectTextsRecursive(root, texts);
        return texts;
    }

    private static void CollectTextsRecursive(Control control, List<string> texts)
    {
        AddIfText(texts, control.Text);

        if (control is ComboBox combo)
        {
            foreach (object item in combo.Items)
            {
                AddIfText(texts, item?.ToString());
            }
        }

        PropertyInfo? segmentsProperty = control.GetType().GetProperty("Segments", BindingFlags.Instance | BindingFlags.Public);
        if (segmentsProperty?.GetValue(control) is string[] segments)
        {
            foreach (string segment in segments)
            {
                AddIfText(texts, segment);
            }
        }

        foreach (Control child in control.Controls)
        {
            CollectTextsRecursive(child, texts);
        }
    }

    private static void AddIfText(List<string> texts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            texts.Add(value);
        }
    }
}
