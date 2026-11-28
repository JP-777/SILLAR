-- M03: baja transaccional de objetos conocidos. Una dependencia u objeto ajeno impide toda la baja.
DO $sales_drop$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'sales') THEN RETURN; END IF;
    DROP TABLE IF EXISTS sales.cart_items, sales.carts, sales.order_lines,
        sales.order_payments, sales.order_status_changes, sales.orders,
        sales.order_series, sales.__migrations RESTRICT;
    DROP FUNCTION IF EXISTS sales.proteger_hechos_manuales() RESTRICT;
    DROP FUNCTION IF EXISTS sales.order_payments_source_protegido() RESTRICT;
    DROP FUNCTION IF EXISTS sales.set_updated_at() RESTRICT;
    DROP SCHEMA sales RESTRICT;
END
$sales_drop$;
