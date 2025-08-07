-- Binary Data Verification Query for PostgreSQL
-- This query helps verify that binary data was migrated correctly

-- Check if binary fields have data and show their characteristics
SELECT 
    "Id",
    CASE 
        WHEN "BinaryField" IS NULL THEN 'NULL'
        ELSE CONCAT('Length: ', LENGTH("BinaryField"), ' bytes')
    END AS BinaryFieldInfo,
    CASE 
        WHEN "VarbinaryField" IS NULL THEN 'NULL'
        ELSE CONCAT('Length: ', LENGTH("VarbinaryField"), ' bytes')
    END AS VarbinaryFieldInfo,
    -- Show first 10 bytes in hex format for verification
    CASE 
        WHEN "BinaryField" IS NULL THEN 'NULL'
        ELSE ENCODE(SUBSTRING("BinaryField", 1, 10), 'hex')
    END AS BinaryFieldHex,
    CASE 
        WHEN "VarbinaryField" IS NULL THEN 'NULL'  
        ELSE ENCODE(SUBSTRING("VarbinaryField", 1, 10), 'hex')
    END AS VarbinaryFieldHex
FROM "Products" 
WHERE "BinaryField" IS NOT NULL OR "VarbinaryField" IS NOT NULL
LIMIT 10;

-- Summary statistics
SELECT 
    COUNT(*) as TotalProducts,
    COUNT("BinaryField") as ProductsWithBinaryField,
    COUNT("VarbinaryField") as ProductsWithVarbinaryField,
    ROUND(AVG(LENGTH("BinaryField"))) as AvgBinaryFieldLength,
    ROUND(AVG(LENGTH("VarbinaryField"))) as AvgVarbinaryFieldLength
FROM "Products";