using System;
using Avalonia.Controls.Notifications;
using System;

namespace FileExplorerCore.Models
{
	public class NotificationModel : INotification
	{
		public string Title { get; set; }

		public string Message { get; set; }

		public NotificationType Type { get; set; }

		public TimeSpan Expiration => TimeSpan.FromSeconds(5);

		public Action OnClick { get; set; }

		public Action OnClose { get; set; }

		public NotificationModel(string title, string message, NotificationType type)
		{
			Title = title ?? throw new ArgumentNullException(nameof(title));
			Message = message ?? throw new ArgumentNullException(nameof(message));
			Type = type;
		}
	}
}
