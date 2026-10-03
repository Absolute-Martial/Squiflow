INSERT INTO tenancy.tenants
    (id, display_name, availability, created_at, suspended_at)
VALUES (@tenant_id, @display_name, @availability, @activated_at, NULL)
