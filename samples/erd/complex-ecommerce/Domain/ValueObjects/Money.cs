namespace ComplexEcommerce.Domain.ValueObjects;

public record Money(decimal Amount, string Currency = "USD");
