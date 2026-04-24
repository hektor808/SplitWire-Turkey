# PR merge için minimum test kuralı

`required-tests` GitHub Actions işi, PR'larda test işinin (`test`) başarılı olmasını zorunlu hale getirmek için tasarlandı.

Repository ayarlarında aşağıdaki zorunlu durum kontrolünü etkinleştirin:

- **required-tests**

Bu kontrol Required status checks listesine eklendiğinde, test başarısızsa PR merge edilemez.
