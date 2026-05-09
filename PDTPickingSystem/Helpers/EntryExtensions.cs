using Microsoft.Maui.Controls;

namespace PDTPickingSystem.Helpers
{
    public static class EntryExtensions
    {
        public static readonly BindableProperty TagProperty =
            BindableProperty.CreateAttached(
                "Tag",
                typeof(object),
                typeof(EntryExtensions),
                null);
        public static object GetTag(BindableObject view)
        {
            return view.GetValue(TagProperty);
        }
        public static void SetTag(BindableObject view, object value)
        {
            view.SetValue(TagProperty, value);
        }
    }
}