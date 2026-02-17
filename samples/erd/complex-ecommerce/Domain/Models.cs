namespace ComplexEcommerce.Domain;

using ComplexEcommerce.Domain.ValueObjects;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }
}

public class Customer : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }

    public ShoppingCart? ShoppingCart { get; set; }
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class Address : BaseEntity
{
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
    public string Country { get; set; } = "USA";

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
}

public class Category : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public abstract class Product : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string SKU { get; set; } = default!;
    public Money Price { get; set; } = default!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductSupplier> ProductSuppliers { get; set; } = new List<ProductSupplier>();
}

public class DigitalProduct : Product
{
    public string DownloadUrl { get; set; } = default!;
    public string? FileExtension { get; set; }
    public long FileSizeInBytes { get; set; }
}

public class PhysicalProduct : Product
{
    public double WeightInKg { get; set; }
    public string Dimensions { get; set; } = default!; // e.g. "10x10x10"
    public bool RequiresInsurance { get; set; }
}

public class Order : BaseEntity
{
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public int? ShippingAddressId { get; set; }
    public Address? ShippingAddress { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
}

public class OrderItem : BaseEntity
{
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; } = default!;

    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

public abstract class Payment : BaseEntity
{
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public Money Amount { get; set; } = default!;

    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;
}

public class CreditCardPayment : Payment
{
    public string CardNumberMasked { get; set; } = default!;
    public string CardHolderName { get; set; } = default!;
}

public class PayPalPayment : Payment
{
    public string PayPalTransactionId { get; set; } = default!;
    public string PayerEmail { get; set; } = default!;
}

public class ShoppingCart : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public ICollection<ShoppingCartItem> CartItems { get; set; } = new List<ShoppingCartItem>();
}

public class ShoppingCartItem : BaseEntity
{
    public int Quantity { get; set; }

    public int ShoppingCartId { get; set; }
    public ShoppingCart ShoppingCart { get; set; } = default!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

public class Supplier : BaseEntity
{
    public string Name { get; set; } = default!;
    public string ContactPerson { get; set; } = default!;
    public ICollection<ProductSupplier> ProductSuppliers { get; set; } = new List<ProductSupplier>();
}

public class ProductSupplier
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;
}

public class Review : BaseEntity
{
    public int Rating { get; set; }
    public string? Comment { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
