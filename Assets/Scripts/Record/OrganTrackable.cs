using UnityEngine;
using System.Reflection;
using Normal.Realtime;

public class OrganTrackable : MonoBehaviour
{
    private RealtimeView _view;
    private string _cachedId;

    void Awake()
    {
        _view = GetComponentInParent<RealtimeView>();
    }

    public string GetId()
    {
        if (!string.IsNullOrEmpty(_cachedId))
            return _cachedId;

        if (_view != null)
        {
            int sharedId;
            if (TryGetRealtimeViewId(_view, out sharedId))
            {
                _cachedId = $"organ_{gameObject.name}_{sharedId}";
                return _cachedId;
            }
        }

        _cachedId = $"organ_{gameObject.name}_{GetInstanceID()}";
        return _cachedId;
    }

    public string GetOrganType()
    {
        return gameObject.name;
    }

    private static bool TryGetRealtimeViewId(RealtimeView view, out int id)
    {
        id = 0;
        if (view == null) return false;

        var t = view.GetType();
        var prop = t.GetProperty("viewID", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            id = (int)prop.GetValue(view);
            return true;
        }

        var field = t.GetField("viewID", BindingFlags.Public | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            id = (int)field.GetValue(view);
            return true;
        }

        field = t.GetField("_viewID", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            id = (int)field.GetValue(view);
            return true;
        }

        return false;
    }
}