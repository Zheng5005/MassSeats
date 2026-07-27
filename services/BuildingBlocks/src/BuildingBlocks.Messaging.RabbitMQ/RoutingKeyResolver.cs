using System.Text;

namespace BuildingBlocks.Messaging.RabbitMQ;

internal static class RoutingKeyResolver
{
    public static string For<TEvent>() => For(typeof(TEvent));

    public static string For(Type eventType)
    {
        var name = eventType.Name;
        var result = new StringBuilder(name.Length + 4);

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];

            if (index > 0 && char.IsUpper(character))
                result.Append('.');

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}
