namespace InternalsViewer.Internals.Columnstore.Metadata.Enums;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
public enum DisableRowGroupReason
{
    EDEVICE_OVERLOAD = 0,
    ERANDOM_DISABLE_TEST = 1,
    ETOO_MANY_KEY_BITS = 2,
    EUNSUPPORT_ENCODE = 3,
    EUNSUPPORT_DATATYPE = 4,
    ENO_64BITS_RLE = 5,
    ENO_ENOUGH_BUFF = 6,
    EDELETE_BITMAP = 7,
    ENO_ENOUGH_DICT_CACHE = 8,
    ENO_BITS_PER_UNIT_HIGH = 9,
    EOVERFLOW_ON_NONINT = 10,
    EDISABLE_REASON_COUNT = 11
}
