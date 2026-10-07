SELECT revision, minimum_unit_price, maximum_unit_price, maximum_decrease_percent,
       maximum_increase_percent, created_by_account_id, created_at
FROM pricing.override_policies WHERE tenant_id = @tenant_id
ORDER BY revision DESC LIMIT 1;
