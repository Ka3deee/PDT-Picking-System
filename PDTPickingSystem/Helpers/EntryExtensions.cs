using Microsoft.Maui.Controls;

namespace PDTPickingSystem.Helpers
{
    public static class EntryExtensions
    {
        // Attached property to mimic WinForms Tag
        public static readonly BindableProperty TagProperty =
            BindableProperty.CreateAttached(
                "Tag",               // Name
                typeof(object),      // Type
                typeof(EntryExtensions), // Owner type
                null);               // Default value

        // Getter
        public static object GetTag(BindableObject view)
        {
            return view.GetValue(TagProperty);
        }

        // Setter
        public static void SetTag(BindableObject view, object value)
        {
            view.SetValue(TagProperty, value);
        }
    }
}
