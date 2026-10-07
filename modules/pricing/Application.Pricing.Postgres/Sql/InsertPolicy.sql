INSERT INTO pricing.override_policies
    (tenant_id, revision, minimum_unit_price, maximum_unit_price, maximum_decrease_percent,
     maximum_increase_percent, created_by_account_id, created_at)
VALUES (@tenant_id, @revision, @minimum, @maximum, @decrease, @increase, @account_id, @created_at);
