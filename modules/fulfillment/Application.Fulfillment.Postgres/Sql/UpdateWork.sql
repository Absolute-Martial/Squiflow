UPDATE fulfillment.works SET revision=@revision,snapshot=@snapshot::jsonb,updated_at=@updated WHERE tenant_id=@tenant AND order_id=@order AND revision=@expected;
