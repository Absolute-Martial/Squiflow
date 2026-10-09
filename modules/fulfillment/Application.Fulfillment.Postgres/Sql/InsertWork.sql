INSERT INTO fulfillment.works(tenant_id,order_id,work_id,revision,source,snapshot,created_at,updated_at) VALUES(@tenant,@order,@work,@revision,@source::jsonb,@snapshot::jsonb,@created,@updated);
