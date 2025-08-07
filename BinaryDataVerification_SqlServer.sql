-- Binary Data Verification Query for SQL Server
-- This query helps verify the source binary data characteristics

-- Check if binary fields have data and show their characteristics
SELECT TOP 10
    Id,
    CASE 
        WHEN BinaryField IS NULL THEN 'NULL'
        ELSE CONCAT('Length: ', LEN(BinaryField), ' bytes')
    END AS BinaryFieldInfo,
    CASE 
        WHEN VarbinaryField IS NULL THEN 'NULL'
        ELSE CONCAT('Length: ', LEN(VarbinaryField), ' bytes')
    END AS VarbinaryFieldInfo,
    -- Show first 10 bytes in hex format for verification
    CASE 
        WHEN BinaryField IS NULL THEN 'NULL'
        ELSE CONVERT(VARCHAR(MAX), SUBSTRING(BinaryField, 1, 10), 2)
    END AS BinaryFieldHex,
    CASE 
        WHEN VarbinaryField IS NULL THEN 'NULL'  
        ELSE CONVERT(VARCHAR(MAX), SUBSTRING(VarbinaryField, 1, 10), 2)
    END AS VarbinaryFieldHex
FROM Products 
WHERE BinaryField IS NOT NULL OR VarbinaryField IS NOT NULL;

-- Summary statistics
SELECT 
    COUNT(*) as TotalProducts,
    COUNT(BinaryField) as ProductsWithBinaryField,
    COUNT(VarbinaryField) as ProductsWithVarbinaryField,
    AVG(LEN(BinaryField)) as AvgBinaryFieldLength,
    AVG(LEN(VarbinaryField)) as AvgVarbinaryFieldLength
FROM Products;