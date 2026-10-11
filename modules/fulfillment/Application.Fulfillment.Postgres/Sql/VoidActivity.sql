UPDATE fulfillment.entry_activity SET active=false,voided_by=@event WHERE tenant_id=@tenant AND order_id=@order AND event_id=@target AND active;
