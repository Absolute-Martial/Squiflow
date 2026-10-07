INSERT INTO orders.quotation_origins(tenant_id,order_id,quotation_id,issued_revision_id,number,revision_number)
VALUES(@tenant_id,@order_id,@quotation_id,@issued_revision_id,@number,@revision_number);
