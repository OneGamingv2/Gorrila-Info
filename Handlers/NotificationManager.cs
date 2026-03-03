using UnityEngine;

public class NotificationManager
{
    public bool notificationsEnabled = true;
    private const float NotificationLifetime = 5f;
    private const float NotificationScale = 0.015f;

    public void ToggleNotifications()
    {
        notificationsEnabled = !notificationsEnabled;
        string status = notificationsEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";
        NotifyDirect($"Notifications: {status}");
    }

    public void Notify(string message)
    {
        if (notificationsEnabled)
            NotifyDirect(message);
    }

    private void NotifyDirect(string message)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform parent = cam.transform.Find("VR_NOTIFICATION_CONTAINER");

        if (parent == null)
        {
            GameObject container = new GameObject("VR_NOTIFICATION_CONTAINER");
            container.transform.SetParent(cam.transform);
            container.transform.localPosition = new Vector3(-0.3f, -0.2f, 0.9f);
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = Vector3.one * NotificationScale;
            parent = container.transform;
        }
        else
        {
            for (int i = 0; i < parent.childCount; i++)
                parent.GetChild(i).localPosition += new Vector3(0, 2f, 0);
        }

        GameObject notification = new GameObject("Notification");
        notification.transform.SetParent(parent, false);

        TextMesh text = notification.AddComponent<TextMesh>();
        text.text = message;
        text.characterSize = 0.1f;
        text.anchor = TextAnchor.UpperLeft;
        text.alignment = TextAlignment.Left;
        text.color = Color.white;
        text.richText = true;

        Object.Destroy(notification, NotificationLifetime);
    }
}
