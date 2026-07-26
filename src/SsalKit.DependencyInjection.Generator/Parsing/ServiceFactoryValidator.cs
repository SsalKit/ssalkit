using System.Globalization;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// The single definition of what makes a <c>[ServiceFactory]</c> interface valid, shared by
/// <c>ServiceFactoryAnalyzer</c> (which turns a failure into an SSAL016-SSAL020 diagnostic) and
/// <see cref="ServiceFactoryParser"/> (which turns a failure into "generate nothing for this
/// interface"). Keeping both on one implementation is what guarantees the generator never emits
/// code for a shape the analyzer reports, and never stays silent about one it rejects.
/// </summary>
internal static class ServiceFactoryValidator
{
    /// <summary>
    /// Validates <paramref name="type"/> as a service factory interface.
    /// </summary>
    /// <param name="type">The type the attribute was applied to.</param>
    /// <param name="compilation">
    /// The compilation the generated code will live in, used to decide accessibility (including
    /// <c>[InternalsVisibleTo]</c> grants and <c>extern alias</c> reachability) via the same
    /// <see cref="TypeAccessibilityChecker"/> the <c>[Service]</c> path uses.
    /// </param>
    public static ServiceFactoryValidation Validate(INamedTypeSymbol type, Compilation compilation)
    {
        // SSAL016: only an interface can be implemented by the generated class. Normally
        // unreachable -- [AttributeUsage(AttributeTargets.Interface)] makes any other target a
        // CS0592 error -- but the attribute is still bound onto the symbol in that case, so the
        // check runs rather than trusting the compiler to have already stopped it.
        if (type.TypeKind != TypeKind.Interface)
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.NotAnInterface, DescribeTypeKind(type));
        }

        // SSAL019: a generic factory interface (or one nested inside a generic type, which carries
        // its container's type parameters) has no single closed form to register a singleton for.
        if (type.Arity > 0)
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.GenericNotSupported, "generic");
        }

        if (ServiceTypeResolver.IsNestedInGenericType(type))
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.GenericNotSupported, "nested inside a generic type");
        }

        // SSAL017: exactly one implementable member, and it has to be an ordinary instance method.
        var members = GetDeclaredMembers(type);
        if (members.Count != 1)
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.MemberShapeInvalid, DescribeMemberCount(members.Count));
        }

        if (members[0] is not IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method)
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.MemberShapeInvalid,
                $"its only member '{members[0].Name}' is not an ordinary, non-static method");
        }

        // SSAL017 (inherited members): the generated class implements the interface, which obliges
        // it to implement everything the interface's *bases* declare too. GetMembers() above only
        // sees what this interface declares itself, so without this check a factory inheriting an
        // interface with members of its own passes validation and is then emitted as a class that
        // does not compile (CS0535).
        var inheritedFailure = DescribeInheritedMembers(type);
        if (inheritedFailure is not null)
        {
            return ServiceFactoryValidation.Failure(
                ServiceFactoryValidationKind.MemberShapeInvalid, inheritedFailure);
        }

        // SSAL018: the method itself must be shaped like `TService Create(SomeEnum key)`.
        var signatureFailure = ValidateSignature(method);
        if (signatureFailure is not null)
        {
            return ServiceFactoryValidation.SignatureFailure(method, signatureFailure);
        }

        // SSAL020: the interface, the key enum, and the return type are all named verbatim by the
        // generated implementation, so each must be reachable from it.
        var keyType = method.Parameters[0].Type;
        var returnType = method.ReturnType;

        foreach (var referenced in new[] { (ITypeSymbol)type, keyType, returnType })
        {
            if (!TypeAccessibilityChecker.IsAccessible(referenced, compilation))
            {
                return ServiceFactoryValidation.Failure(
                    ServiceFactoryValidationKind.Inaccessible,
                    SymbolFacts.ToFqn(referenced));
            }
        }

        return ServiceFactoryValidation.Success(method, keyType, returnType);
    }

    /// <summary>
    /// Returns the reason <paramref name="method"/> cannot be forwarded to
    /// <c>GetRequiredKeyedService&lt;TReturn&gt;(key)</c>, or <see langword="null"/> if it can.
    /// </summary>
    private static string? ValidateSignature(IMethodSymbol method)
    {
        if (method.Arity > 0)
        {
            return "it is generic";
        }

        if (method.Parameters.Length != 1)
        {
            return method.Parameters.Length == 0
                ? "it has no parameters"
                : $"it has {method.Parameters.Length.ToString(CultureInfo.InvariantCulture)} parameters";
        }

        var parameter = method.Parameters[0];

        // `ref`/`out`/`in`/`ref readonly`: an enum key passed by reference has no meaning for a
        // keyed lookup (the value is boxed into `object?` either way), and rejecting every RefKind
        // keeps the emitted signature a plain by-value one.
        if (parameter.RefKind != RefKind.None)
        {
            return $"its parameter '{parameter.Name}' is passed by reference";
        }

        if (parameter.Type.TypeKind != TypeKind.Enum)
        {
            return $"its parameter '{parameter.Name}' is of type '{SymbolFacts.ToFqn(parameter.Type)}', which is not an enum type";
        }

        if (method.ReturnsVoid)
        {
            return "it returns void";
        }

        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            return "it returns by reference";
        }

        return null;
    }

    /// <summary>
    /// The interface's declared members as a consumer sees them: property/event accessors are
    /// folded back into the property/event they belong to (they are separate
    /// <see cref="IMethodSymbol"/>s in <see cref="INamedTypeSymbol.GetMembers()"/>), so a
    /// single-property interface is reported as declaring one member rather than two.
    /// </summary>
    /// <remarks>
    /// Everything else -- an extra method, a field or constant, a nested type -- is counted, which
    /// is what makes SSAL017's rule "exactly one member" rather than "exactly one method plus
    /// whatever else you like". Inherited members are deliberately <em>not</em> counted here; they
    /// are a separate rule with a message of its own (see <see cref="DescribeInheritedMembers"/>),
    /// because "you declared two members" and "your base interface declares one" are fixed in
    /// different places.
    /// </remarks>
    private static List<ISymbol> GetDeclaredMembers(INamedTypeSymbol type)
    {
        var members = new List<ISymbol>();

        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol { AssociatedSymbol: not null })
            {
                continue;
            }

            members.Add(member);
        }

        return members;
    }

    /// <summary>
    /// Describes the first base interface (ordinal by fully-qualified name, so the message does not
    /// depend on <see cref="INamedTypeSymbol.AllInterfaces"/>'s enumeration order) that obliges the
    /// generated implementation to implement a member it knows nothing about, or
    /// <see langword="null"/> when every inherited interface is safe to carry.
    /// </summary>
    /// <remarks>
    /// A <em>marker</em> base interface -- one declaring nothing at all -- is explicitly allowed: it
    /// imposes no implementation burden, and marker interfaces are a normal way to group factories
    /// for a consumer's own scanning. The same reasoning admits a base interface whose every member
    /// is static, a nested type, or carries a default implementation: none of those has to be
    /// implemented by the class either.
    /// </remarks>
    private static string? DescribeInheritedMembers(INamedTypeSymbol type)
    {
        List<(string Fqn, List<string> Members)>? offenders = null;

        foreach (var baseInterface in type.AllInterfaces)
        {
            var required = GetMembersRequiringImplementation(baseInterface);
            if (required.Count == 0)
            {
                continue;
            }

            offenders ??= [];
            offenders.Add((SymbolFacts.ToFqn(baseInterface), required));
        }

        if (offenders is null)
        {
            return null;
        }

        var (fqn, members) = offenders.OrderBy(offender => offender.Fqn, StringComparer.Ordinal).First();
        var memberList = string.Join(", ", members.OrderBy(name => name, StringComparer.Ordinal).Select(name => $"'{name}'"));
        var count = members.Count.ToString(CultureInfo.InvariantCulture);

        return $"it inherits '{fqn}', which declares {count} member(s) the generated implementation would have to implement ({memberList})";
    }

    /// <summary>
    /// The members of <paramref name="baseInterface"/> that an implementing class must supply a body
    /// for: instance members without a default implementation. Accessors are folded into the
    /// property/event they belong to, exactly as <see cref="GetDeclaredMembers"/> does, so a base
    /// interface declaring one property is reported as one member rather than two.
    /// </summary>
    private static List<string> GetMembersRequiringImplementation(INamedTypeSymbol baseInterface)
    {
        var required = new List<string>();

        foreach (var member in baseInterface.GetMembers())
        {
            // Static members (including constants) and nested types are never inherited obligations.
            if (member.IsStatic || member is INamedTypeSymbol)
            {
                continue;
            }

            if (member is IMethodSymbol { AssociatedSymbol: not null })
            {
                continue;
            }

            // A default interface implementation is non-abstract, and the generated class inherits
            // it rather than having to write one.
            if (!member.IsAbstract)
            {
                continue;
            }

            required.Add(member.Name);
        }

        return required;
    }

    private static string DescribeMemberCount(int count) => count == 0
        ? "it declares no members"
        : $"it declares {count.ToString(CultureInfo.InvariantCulture)} members";

    private static string DescribeTypeKind(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Class => "a class",
        TypeKind.Struct => "a struct",
        TypeKind.Enum => "an enum",
        TypeKind.Delegate => "a delegate",
        _ => "not an interface",
    };
}

/// <summary>
/// Why <see cref="ServiceFactoryValidator.Validate"/> rejected an interface, mapped one-to-one onto
/// the SSAL016-SSAL020 diagnostics by <c>ServiceFactoryAnalyzer</c>.
/// </summary>
internal enum ServiceFactoryValidationKind
{
    /// <summary>The interface is a valid service factory.</summary>
    Success,

    /// <summary>SSAL016: the attribute was applied to something other than an interface.</summary>
    NotAnInterface,

    /// <summary>
    /// SSAL017: the interface does not declare exactly one ordinary instance method, or it inherits
    /// a base interface with implementable members of its own.
    /// </summary>
    MemberShapeInvalid,

    /// <summary>SSAL018: the single method is not shaped like <c>TService Create(SomeEnum key)</c>.</summary>
    SignatureInvalid,

    /// <summary>SSAL019: the interface is generic, or nested inside a generic type.</summary>
    GenericNotSupported,

    /// <summary>SSAL020: a type the generated implementation must name is not accessible from it.</summary>
    Inaccessible,
}

/// <summary>
/// The outcome of validating one <c>[ServiceFactory]</c> interface: either
/// <see cref="ServiceFactoryValidationKind.Success"/> plus the resolved symbols the emitter needs,
/// or a failure kind plus the text spliced into its diagnostic message.
/// </summary>
internal readonly struct ServiceFactoryValidation
{
    private ServiceFactoryValidation(
        ServiceFactoryValidationKind kind,
        string? detail,
        IMethodSymbol? method,
        ITypeSymbol? keyType,
        ITypeSymbol? returnType)
    {
        Kind = kind;
        Detail = detail;
        Method = method;
        KeyType = keyType;
        ReturnType = returnType;
    }

    /// <summary>The validation outcome.</summary>
    public ServiceFactoryValidationKind Kind { get; }

    /// <summary>
    /// The message argument for the corresponding diagnostic: a reason phrase for SSAL016-SSAL019,
    /// or the offending type's fully-qualified name for SSAL020. <see langword="null"/> only for
    /// <see cref="ServiceFactoryValidationKind.Success"/>.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// The factory method, non-<see langword="null"/> for
    /// <see cref="ServiceFactoryValidationKind.Success"/> and for
    /// <see cref="ServiceFactoryValidationKind.SignatureInvalid"/> (whose diagnostic names it).
    /// </summary>
    public IMethodSymbol? Method { get; }

    /// <summary>The enum type of the method's single parameter. Only set on success.</summary>
    public ITypeSymbol? KeyType { get; }

    /// <summary>The method's return type, i.e. the resolved service type. Only set on success.</summary>
    public ITypeSymbol? ReturnType { get; }

    public static ServiceFactoryValidation Success(IMethodSymbol method, ITypeSymbol keyType, ITypeSymbol returnType) =>
        new(ServiceFactoryValidationKind.Success, null, method, keyType, returnType);

    public static ServiceFactoryValidation Failure(ServiceFactoryValidationKind kind, string detail) =>
        new(kind, detail, null, null, null);

    public static ServiceFactoryValidation SignatureFailure(IMethodSymbol method, string detail) =>
        new(ServiceFactoryValidationKind.SignatureInvalid, detail, method, null, null);
}
