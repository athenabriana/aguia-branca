using AguiaBranca.Domain.Common;

namespace AguiaBranca.Api.Http;

/// <summary>Restringe <c>{id:objectid}</c> a 24 caracteres hexadecimais; ids inválidos viram 404 (nunca chegam ao banco).</summary>
public sealed class ObjectIdRouteConstraint : IRouteConstraint
{
    public const string Name = "objectid";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection) =>
        values.TryGetValue(routeKey, out var value) && EntityId.IsValid(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
}
